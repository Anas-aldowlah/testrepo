using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class AddPosDraftConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "draft_revision",
                table: "sales",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "edit_lock_expires_at",
                table: "sales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "edit_locked_by",
                table: "sales",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "edit_session_id",
                table: "sales",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "draft_revision",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "edit_lock_expires_at",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "edit_locked_by",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "edit_session_id",
                table: "sales");
        }
    }
}
