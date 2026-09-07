using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class YAGOT03SiteStateSyncCheckpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "site_state_sync_checkpoints",
                columns: table => new
                {
                    siteid = table.Column<int>(type: "integer", nullable: false),
                    lastattemptatutc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lastsuccessatutc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lastobservedremoterevision = table.Column<long>(type: "bigint", nullable: true),
                    consecutivefailures = table.Column<int>(type: "integer", nullable: false),
                    lastfailurecode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updatedatutc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("site_state_sync_checkpoints_pkey", x => x.siteid);
                    table.CheckConstraint("ck_site_state_sync_checkpoints_failures", "consecutivefailures >= 0");
                    table.CheckConstraint("ck_site_state_sync_checkpoints_remote_revision", "lastobservedremoterevision IS NULL OR lastobservedremoterevision >= 1");
                    table.CheckConstraint("ck_site_state_sync_checkpoints_site_id", "siteid = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_state_sync_checkpoints");
        }
    }
}
