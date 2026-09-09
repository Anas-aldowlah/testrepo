using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeEventTimeDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "visitdate",
                table: "visits",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "sales_days",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "sales",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "sale_payments",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "createdat",
                table: "products",
                type: "timestamp without time zone",
                nullable: true,
                defaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true,
                oldDefaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimeState",
                table: "orders",
                type: "timestamp without time zone",
                nullable: true,
                defaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true,
                oldDefaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "createdat",
                table: "carts",
                type: "timestamp without time zone",
                nullable: true,
                defaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true,
                oldDefaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "visitdate",
                table: "visits",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "sales_days",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "sales",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "sale_payments",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')");

            migrationBuilder.AlterColumn<DateTime>(
                name: "createdat",
                table: "products",
                type: "timestamp without time zone",
                nullable: true,
                defaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true,
                oldDefaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')");

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimeState",
                table: "orders",
                type: "timestamp without time zone",
                nullable: true,
                defaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true,
                oldDefaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')");

            migrationBuilder.AlterColumn<DateTime>(
                name: "createdat",
                table: "carts",
                type: "timestamp without time zone",
                nullable: true,
                defaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true,
                oldDefaultValueSql: "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')");
        }
    }
}
