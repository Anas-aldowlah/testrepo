using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class Phase3ACapabilitySnapshotFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "capability_event_receipts",
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
                    table.PrimaryKey("capability_event_receipts_pkey", x => x.deliveryid);
                    table.CheckConstraint("ck_capability_receipts_decision", "decision IN ('Applied', 'Equal', 'Stale', 'EqualConflict')");
                    table.CheckConstraint("ck_capability_receipts_hash_length", "octet_length(payloadsha256) = 32");
                    table.CheckConstraint("ck_capability_receipts_revision", "revision >= 1");
                    table.CheckConstraint("ck_capability_receipts_site_id", "siteid >= 1");
                });

            migrationBuilder.CreateTable(
                name: "capability_sync_checkpoints",
                columns: table => new
                {
                    siteid = table.Column<int>(type: "integer", nullable: false),
                    lastattemptedrevision = table.Column<long>(type: "bigint", nullable: true),
                    lastobservedremoterevision = table.Column<long>(type: "bigint", nullable: true),
                    lastsuccessfullyappliedrevision = table.Column<long>(type: "bigint", nullable: true),
                    lastattemptatutc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lastsuccessatutc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    health = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    lastfailurecategory = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updatedatutc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("capability_sync_checkpoints_pkey", x => x.siteid);
                    table.CheckConstraint("ck_capability_checkpoint_applied_revision", "lastsuccessfullyappliedrevision IS NULL OR lastsuccessfullyappliedrevision >= 1");
                    table.CheckConstraint("ck_capability_checkpoint_attempted_revision", "lastattemptedrevision IS NULL OR lastattemptedrevision >= 1");
                    table.CheckConstraint("ck_capability_checkpoint_observed_revision", "lastobservedremoterevision IS NULL OR lastobservedremoterevision >= 1");
                    table.CheckConstraint("ck_capability_checkpoint_site_id", "siteid >= 1");
                });

            migrationBuilder.CreateTable(
                name: "local_capability_snapshots",
                columns: table => new
                {
                    siteid = table.Column<int>(type: "integer", nullable: false),
                    contractversion = table.Column<int>(type: "integer", nullable: false),
                    catalogversion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    snapshotjson = table.Column<string>(type: "jsonb", nullable: false),
                    payloadsha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    generatedatutc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effectiveatutc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    appliedatutc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("local_capability_snapshots_pkey", x => x.siteid);
                    table.CheckConstraint("ck_local_capability_contract_version", "contractversion = 1");
                    table.CheckConstraint("ck_local_capability_hash_length", "octet_length(payloadsha256) = 32");
                    table.CheckConstraint("ck_local_capability_revision", "revision >= 1");
                    table.CheckConstraint("ck_local_capability_site_id", "siteid >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_capability_event_receipts_site_revision",
                table: "capability_event_receipts",
                columns: new[] { "siteid", "revision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "capability_event_receipts");

            migrationBuilder.DropTable(
                name: "capability_sync_checkpoints");

            migrationBuilder.DropTable(
                name: "local_capability_snapshots");
        }
    }
}
