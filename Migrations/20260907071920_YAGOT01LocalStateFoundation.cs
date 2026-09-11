using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class YAGOT01LocalStateFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "local_site_state_snapshots",
                columns: table => new
                {
                    siteid = table.Column<int>(type: "integer", nullable: false),
                    contractversion = table.Column<int>(type: "integer", nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    effectiveatutc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expiresatutc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sitename = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    siteurl = table.Column<string>(type: "text", nullable: false),
                    startdate = table.Column<DateOnly>(type: "date", nullable: false),
                    originaldurationdays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("local_site_state_snapshots_pkey", x => x.siteid);
                    table.CheckConstraint("ck_local_site_state_contract_version", "contractversion = 1");
                    table.CheckConstraint("ck_local_site_state_duration", "originaldurationdays > 0");
                    table.CheckConstraint("ck_local_site_state_mode", "mode IN ('Online', 'Development', 'Offline')");
                    table.CheckConstraint("ck_local_site_state_revision", "revision >= 1");
                    table.CheckConstraint("ck_local_site_state_site_id", "siteid = 1");
                });

            migrationBuilder.CreateTable(
                name: "site_state_event_receipts",
                columns: table => new
                {
                    deliveryid = table.Column<Guid>(type: "uuid", nullable: false),
                    siteid = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    payloadsha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recordedatutc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("site_state_event_receipts_pkey", x => x.deliveryid);
                    table.CheckConstraint("ck_site_state_event_receipts_decision", "decision IN ('Applied', 'Equal', 'EqualConflict', 'Stale')");
                    table.CheckConstraint("ck_site_state_event_receipts_hash_length", "octet_length(payloadsha256) = 32");
                    table.CheckConstraint("ck_site_state_event_receipts_revision", "revision >= 1");
                    table.CheckConstraint("ck_site_state_event_receipts_site_id", "siteid = 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_site_state_event_receipts_site_revision",
                table: "site_state_event_receipts",
                columns: new[] { "siteid", "revision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "local_site_state_snapshots");

            migrationBuilder.DropTable(
                name: "site_state_event_receipts");
        }
    }
}
