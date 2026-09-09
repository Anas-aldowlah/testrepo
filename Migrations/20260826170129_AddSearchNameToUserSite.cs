using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchNameToUserSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchName",
                table: "UserSite",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SearchNameSyncVersion",
                table: "UserSite",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchName",
                table: "UserSite");

            migrationBuilder.DropColumn(
                name: "SearchNameSyncVersion",
                table: "UserSite");
        }
    }
}
